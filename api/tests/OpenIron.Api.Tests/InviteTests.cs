using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Contracts;
using OpenIron.Api.Services;

namespace OpenIron.Api.Tests;

// Sign-up mode is instance-wide, so these get a database of their own and set the mode they need.
public class InviteTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Test users are created through the API, which needs sign-up open, so every test starts from open and
    // then closes it itself if it needs to.
    private async Task<HttpClient> CreateMemberAsync(string username)
    {
        await SetRegistrationOpen(true);
        return await factory.CreateAuthenticatedAsync(username);
    }

    private async Task<HttpClient> CreateAdminAsync(string username)
    {
        var client = await CreateMemberAsync(username);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.FirstAsync(u => u.Username == username)).IsAdmin = true;
            await db.SaveChangesAsync();
        });
        return client;
    }

    private Task SetRegistrationOpen(bool open) => factory.WithDbAsync(async db =>
    {
        (await db.ServerSettings.SingleAsync()).RegistrationOpen = open;
        await db.SaveChangesAsync();
    });

    private static async Task<CreatedInviteDto> CreateInvite(HttpClient admin, string? note = null, int? days = 7)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/invites", new CreateInviteRequest(note, days));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedInviteDto>())!;
    }

    private Task<HttpResponseMessage> Register(string username, string? code) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(username, "supersecret1", code));

    [Fact]
    public async Task A_valid_code_lets_one_person_sign_up_and_only_once()
    {
        var admin = await CreateAdminAsync("inv_admin1");
        await SetRegistrationOpen(false);
        var invite = await CreateInvite(admin, "for Sam");

        var first = await Register("inv_sam", invite.Code);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await Register("inv_not_sam", invite.Code);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
        await factory.WithDbAsync(async db => Assert.False(await db.Users.AnyAsync(u => u.Username == "inv_not_sam")));

        var listed = (await admin.GetFromJsonAsync<List<InviteDto>>("/api/admin/invites"))!.Single(i => i.Id == invite.Invite.Id);
        Assert.Equal("used", listed.Status);
        Assert.Equal("inv_sam", listed.UsedBy);
        Assert.Equal("for Sam", listed.Note);
    }

    [Fact]
    public async Task Without_a_valid_code_sign_up_is_refused_while_by_invitation()
    {
        var admin = await CreateAdminAsync("inv_admin2");
        await SetRegistrationOpen(false);
        await CreateInvite(admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await Register("inv_nocode", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Register("inv_blank", "   ")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Register("inv_wrong", "AAAA-BBBB-CCCC")).StatusCode);
        await factory.WithDbAsync(async db => Assert.False(await db.Users.AnyAsync(u => u.Username.StartsWith("inv_no") || u.Username == "inv_wrong")));
    }

    [Fact]
    public async Task Codes_ignore_case_spacing_and_hyphens()
    {
        var admin = await CreateAdminAsync("inv_admin3");
        await SetRegistrationOpen(false);
        var invite = await CreateInvite(admin);

        var sloppy = " " + invite.Code.Replace("-", " ").ToLowerInvariant() + " ";
        Assert.Equal(HttpStatusCode.OK, (await Register("inv_sloppy", sloppy)).StatusCode);
    }

    [Fact]
    public async Task A_failed_sign_up_does_not_use_up_the_code()
    {
        var admin = await CreateAdminAsync("inv_admin4");
        await CreateMemberAsync("inv_taken");
        await SetRegistrationOpen(false);
        var invite = await CreateInvite(admin);

        // Valid code, but the username is taken: this must not burn the code.
        Assert.Equal(HttpStatusCode.Conflict, (await Register("inv_taken", invite.Code)).StatusCode);
        // Nor should a password that fails validation.
        var weak = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest("inv_weak", "short", invite.Code));
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await Register("inv_second_try", invite.Code)).StatusCode);
    }

    [Fact]
    public async Task Without_a_code_usernames_cannot_be_probed()
    {
        await CreateMemberAsync("inv_exists");
        await SetRegistrationOpen(false);

        var existing = await Register("inv_exists", null);
        var missing = await Register("inv_nonexistent", null);

        Assert.Equal(HttpStatusCode.Forbidden, existing.StatusCode);
        Assert.Equal(existing.StatusCode, missing.StatusCode);
    }

    [Fact]
    public async Task Expired_and_revoked_invites_are_rejected()
    {
        var admin = await CreateAdminAsync("inv_admin5");
        await SetRegistrationOpen(false);
        var expired = await CreateInvite(admin);
        var revoked = await CreateInvite(admin);

        await factory.WithDbAsync(async db =>
        {
            (await db.Invites.SingleAsync(i => i.Id == expired.Invite.Id)).ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/invites/{revoked.Invite.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await Register("inv_late", expired.Code)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Register("inv_revoked", revoked.Code)).StatusCode);

        var statuses = (await admin.GetFromJsonAsync<List<InviteDto>>("/api/admin/invites"))!.ToDictionary(i => i.Id, i => i.Status);
        Assert.Equal("expired", statuses[expired.Invite.Id]);
        Assert.Equal("revoked", statuses[revoked.Invite.Id]);
    }

    [Fact]
    public async Task Codes_are_not_needed_or_used_while_sign_up_is_open()
    {
        var admin = await CreateAdminAsync("inv_admin6");
        var invite = await CreateInvite(admin);
        await SetRegistrationOpen(true);

        Assert.Equal(HttpStatusCode.OK, (await Register("inv_open_nocode", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Register("inv_open_withcode", invite.Code)).StatusCode);

        var status = (await admin.GetFromJsonAsync<List<InviteDto>>("/api/admin/invites"))!.Single(i => i.Id == invite.Invite.Id).Status;
        Assert.Equal("active", status);
    }

    [Fact]
    public async Task The_code_is_shown_once_and_only_its_hash_is_stored()
    {
        var admin = await CreateAdminAsync("inv_admin7");
        var invite = await CreateInvite(admin, "secret check");

        var list = await (await admin.GetAsync("/api/admin/invites")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(invite.Code, list);
        Assert.DoesNotContain(InviteService.Normalize(invite.Code), list);

        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Invites.SingleAsync(i => i.Id == invite.Invite.Id);
            Assert.Equal(InviteService.Hash(invite.Code), stored.CodeHash);
            Assert.DoesNotContain(InviteService.Normalize(invite.Code), stored.CodeHash, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Expiry_and_notes_are_optional_and_validated()
    {
        var admin = await CreateAdminAsync("inv_admin8");

        var forever = await CreateInvite(admin, null, null);
        Assert.Null(forever.Invite.ExpiresAt);
        Assert.Null(forever.Invite.Note);

        var week = await CreateInvite(admin, "  trimmed  ", 7);
        Assert.Equal("trimmed", week.Invite.Note);
        Assert.InRange((week.Invite.ExpiresAt!.Value - DateTime.UtcNow).TotalDays, 6.9, 7.1);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/admin/invites", new CreateInviteRequest(new string('x', 101), 7))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/admin/invites", new CreateInviteRequest(null, 0))).StatusCode);
    }

    [Fact]
    public async Task Used_invites_cannot_be_revoked_and_unknown_ones_are_404()
    {
        var admin = await CreateAdminAsync("inv_admin9");
        await SetRegistrationOpen(false);
        var invite = await CreateInvite(admin);
        await Register("inv_used_one", invite.Code);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/admin/invites/{invite.Invite.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/admin/invites/{Guid.NewGuid()}")).StatusCode);

        var other = await CreateInvite(admin);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/invites/{other.Invite.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/invites/{other.Invite.Id}")).StatusCode);
    }

    [Fact]
    public async Task Only_admins_can_manage_invites()
    {
        var member = await CreateMemberAsync("inv_member");
        var anon = factory.CreateClient();
        var id = Guid.NewGuid();

        foreach (var (client, expected) in new[] { (member, HttpStatusCode.Forbidden), (anon, HttpStatusCode.Unauthorized) })
        {
            Assert.Equal(expected, (await client.GetAsync("/api/admin/invites")).StatusCode);
            Assert.Equal(expected, (await client.PostAsJsonAsync("/api/admin/invites", new CreateInviteRequest(null, 7))).StatusCode);
            Assert.Equal(expected, (await client.DeleteAsync($"/api/admin/invites/{id}")).StatusCode);
        }
    }

    [Fact]
    public async Task Deleting_the_person_who_used_an_invite_keeps_the_record()
    {
        var admin = await CreateAdminAsync("inv_admin10");
        await SetRegistrationOpen(false);
        var invite = await CreateInvite(admin);
        var response = await Register("inv_leaver", invite.Code);
        var userId = (await response.Content.ReadFromJsonAsync<AuthResponse>())!.User.Id;

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/users/{userId}")).StatusCode);

        var listed = (await admin.GetFromJsonAsync<List<InviteDto>>("/api/admin/invites"))!.Single(i => i.Id == invite.Invite.Id);
        Assert.Equal("used", listed.Status);
        Assert.Null(listed.UsedBy);
    }

    [Fact]
    public void Generated_codes_are_well_formed_and_distinct()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => InviteService.NewCode()).ToList();

        Assert.Equal(500, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches("^[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$", c));
    }

    [Fact]
    public void Normalising_and_hashing_ignore_formatting_but_not_content()
    {
        Assert.Equal("ABCD2345", InviteService.Normalize(" abcd-2345 "));
        Assert.Equal(InviteService.Hash("abcd-2345"), InviteService.Hash("ABCD 2345"));
        Assert.NotEqual(InviteService.Hash("ABCD-2345"), InviteService.Hash("ABCD-2346"));
        Assert.Equal(64, InviteService.Hash("x").Length);
    }
}
