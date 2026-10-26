import type { Invite } from '../api/types'

/** A link that opens the sign-up form with the code already filled in. */
export function inviteLink(origin: string, code: string): string {
  return `${origin}/?invite=${encodeURIComponent(code)}`
}

export function inviteStatusLabel(invite: Invite): string {
  switch (invite.status) {
    case 'active':
      return 'Active'
    case 'used':
      // The account may have been deleted since, which removes the link to it.
      return invite.usedBy ? `Used by ${invite.usedBy}` : 'Used (account deleted)'
    case 'expired':
      return 'Expired'
    case 'revoked':
      return 'Revoked'
  }
}

/** Copies text, falling back to a hidden textarea where the clipboard API isn't available (plain-http LAN hosts). */
export async function copyText(text: string): Promise<boolean> {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text)
      return true
    }
  } catch {
    // fall through to the textarea route
  }
  const area = document.createElement('textarea')
  area.value = text
  area.setAttribute('readonly', '')
  area.style.position = 'fixed'
  area.style.opacity = '0'
  document.body.appendChild(area)
  area.select()
  try {
    return document.execCommand('copy')
  } catch {
    return false
  } finally {
    document.body.removeChild(area)
  }
}
