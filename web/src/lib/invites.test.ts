import { describe, expect, it } from 'vitest'
import type { Invite } from '../api/types'
import { inviteLink, inviteStatusLabel } from './invites'

const invite = (patch: Partial<Invite>): Invite => ({
  id: '1',
  note: null,
  createdAt: '2026-01-01T00:00:00Z',
  expiresAt: null,
  status: 'active',
  usedBy: null,
  usedAt: null,
  ...patch,
})

describe('inviteLink', () => {
  it('puts the code in the query string of the site root', () => {
    expect(inviteLink('https://gym.example.com', 'ABCD-2345-WXYZ')).toBe('https://gym.example.com/?invite=ABCD-2345-WXYZ')
  })

  it('escapes anything that is not URL-safe', () => {
    expect(inviteLink('http://x', 'a b&c')).toBe('http://x/?invite=a%20b%26c')
  })
})

describe('inviteStatusLabel', () => {
  it('names who used it', () => {
    expect(inviteStatusLabel(invite({ status: 'used', usedBy: 'sam' }))).toBe('Used by sam')
  })

  it('copes with the account having been deleted', () => {
    expect(inviteStatusLabel(invite({ status: 'used', usedBy: null }))).toBe('Used (account deleted)')
  })

  it('labels the other states', () => {
    expect(inviteStatusLabel(invite({ status: 'active' }))).toBe('Active')
    expect(inviteStatusLabel(invite({ status: 'expired' }))).toBe('Expired')
    expect(inviteStatusLabel(invite({ status: 'revoked' }))).toBe('Revoked')
  })
})
