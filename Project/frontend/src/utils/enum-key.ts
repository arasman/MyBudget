/**
 * Maps server-side kebab-case role values to camelCase i18n key suffixes.
 * Server sends e.g. "read-only"; i18n key is "enums.role.readOnly".
 */
const roleKeyMap: Record<string, string> = {
  admin: 'admin',
  operator: 'operator',
  owner: 'owner',
  'read-only': 'readOnly',
}

/**
 * Converts a server-side role string to its i18n key suffix.
 * Falls back to the original value if no mapping is found.
 */
export function toRoleKey(value: string): string {
  return roleKeyMap[value] ?? value
}
