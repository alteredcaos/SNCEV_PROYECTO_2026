import React, { createContext, useContext } from 'react';
import { useAuth } from '@/lib/AuthContext';
import { getUserRoles } from '@/lib/roles';

const RoleContext = createContext({ role: null, roles: [] });

export function RoleProvider({ children }) {
  const { user } = useAuth();
  const roles = getUserRoles(user);
  return <RoleContext.Provider value={{ role: roles[0] || null, roles }}>{children}</RoleContext.Provider>;
}

export const useRole = () => useContext(RoleContext);
