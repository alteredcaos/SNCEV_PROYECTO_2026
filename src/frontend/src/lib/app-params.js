// =============================================================================
// SNCEV — Parámetros de configuración del cliente
// =============================================================================

const isNode = typeof window === 'undefined';

const isClearAccessTokenRequested = () =>
  !isNode && new URLSearchParams(window.location.search).get("clear_access_token") === 'true';

export const clearStoredAccessToken = () => {
  if (!isNode) {
    window.localStorage.removeItem('sncev_token');
    window.localStorage.removeItem('token');
  }
};

export const getStoredAccessToken = () => {
  if (!isNode) {
    return window.localStorage.getItem('sncev_token') || window.localStorage.getItem('token') || '';
  }
  return '';
};

export const setStoredAccessToken = (token) => {
  if (!isNode) {
    window.localStorage.setItem('sncev_token', token);
    window.localStorage.setItem('token', token);
  }
};

const getAppParams = () => {
  if (isClearAccessTokenRequested()) {
    clearStoredAccessToken();
  }
  return {
    appId: 'sncev',
    token: getStoredAccessToken(),
    apiUrl: import.meta.env.VITE_API_URL || '/api/v1',
    backendUrl: import.meta.env.VITE_BACKEND_URL || 'http://localhost:8000',
  };
};

export const appParams = {
  ...getAppParams(),
};
