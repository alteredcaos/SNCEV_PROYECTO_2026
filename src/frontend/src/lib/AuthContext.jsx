import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { apiClient, clearStoredSession, getStoredToken } from '@/api/apiClient';

const AuthContext = createContext(null);

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoadingAuth, setIsLoadingAuth] = useState(true);
  const [isLoadingPublicSettings, setIsLoadingPublicSettings] = useState(true);
  const [authError, setAuthError] = useState(null);
  const [authChecked, setAuthChecked] = useState(false);
  const [appPublicSettings, setAppPublicSettings] = useState({
    id: 'sncev-app', name: 'SNCEV — Cartilla Electrónica de Vacunación', public_settings: {},
  });

  const clearAuthState = useCallback(() => {
    clearStoredSession();
    setUser(null);
    setIsAuthenticated(false);
  }, []);

  const checkUserAuth = useCallback(async () => {
    setIsLoadingAuth(true);
    setAuthError(null);
    try {
      if (!getStoredToken()) {
        clearAuthState();
        return null;
      }
      const currentUser = await apiClient.auth.me();
      setUser(currentUser);
      setIsAuthenticated(true);
      return currentUser;
    } catch (error) {
      clearAuthState();
      if (error.status !== 401 && error.status !== 403) {
        setAuthError({ type: error.code === 'NETWORK_ERROR' ? 'network_error' : 'auth_error', message: error.message });
      }
      return null;
    } finally {
      setAuthChecked(true);
      setIsLoadingAuth(false);
    }
  }, [clearAuthState]);

  const checkAppState = useCallback(async () => {
    setIsLoadingPublicSettings(true);
    try {
      setAppPublicSettings(await apiClient.app.getPublicSettings());
    } finally {
      setIsLoadingPublicSettings(false);
    }
    await checkUserAuth();
  }, [checkUserAuth]);

  useEffect(() => { checkAppState(); }, [checkAppState]);

  const login = useCallback(async (username, password) => {
    setIsLoadingAuth(true);
    setAuthError(null);
    try {
      const result = await apiClient.auth.login(username, password);
      setUser(result.user);
      setIsAuthenticated(true);
      setAuthChecked(true);
      return result;
    } catch (error) {
      clearAuthState();
      throw error;
    } finally {
      setIsLoadingAuth(false);
    }
  }, [clearAuthState]);

  const logout = useCallback(() => {
    apiClient.auth.logout();
    setUser(null);
    setIsAuthenticated(false);
    setAuthChecked(true);
  }, []);

  return (
    <AuthContext.Provider value={{
      user, isAuthenticated, isLoadingAuth, isLoadingPublicSettings, authError,
      appPublicSettings, authChecked, login, logout, checkUserAuth, checkAppState,
    }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
};
