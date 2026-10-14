export const ROLES = Object.freeze({
  ADMINISTRADOR: 'Administrador',
  CAPTURADOR: 'Capturador',
  DIRECTIVO: 'Directivo',
  AUDITOR: 'Auditor',
});

export const TODOS_LOS_ROLES = Object.values(ROLES);

export function getUserRoles(user) {
  if (!user) return [];
  if (Array.isArray(user.roles)) return user.roles;
  if (Array.isArray(user.role)) return user.role;
  if (typeof user.rol === 'string') return [user.rol];
  if (typeof user.role === 'string') return [user.role];
  return [];
}

export function hasAnyRole(user, allowedRoles = []) {
  if (!allowedRoles?.length) return true;
  const current = getUserRoles(user);
  return allowedRoles.some((role) => current.includes(role));
}
