// =============================================================================
// SNCEV — Cliente API ASP.NET Core
// =============================================================================

const API_BASE_URL = (import.meta.env.VITE_API_URL || '/api/v1').replace(/\/$/, '');

export const getStoredToken = () => {
  if (typeof window === 'undefined') return null;
  return localStorage.getItem('sncev_token');
};

export const setStoredToken = (token) => {
  if (typeof window === 'undefined') return;
  if (token) localStorage.setItem('sncev_token', token);
  else localStorage.removeItem('sncev_token');
};

export const getStoredUser = () => {
  if (typeof window === 'undefined') return null;
  try {
    const raw = localStorage.getItem('sncev_user');
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
};

export const setStoredUser = (user) => {
  if (typeof window === 'undefined') return;
  if (user) localStorage.setItem('sncev_user', JSON.stringify(user));
  else localStorage.removeItem('sncev_user');
};

export const clearStoredSession = () => {
  if (typeof window === 'undefined') return;
  localStorage.removeItem('sncev_token');
  localStorage.removeItem('sncev_user');
  // Limpieza de claves heredadas del frontend React/Python anterior.
  localStorage.removeItem('token');
};

async function request(endpoint, options = {}) {
  const url = endpoint.startsWith('http')
    ? endpoint
    : `${API_BASE_URL}${endpoint.startsWith('/') ? '' : '/'}${endpoint}`;

  const headers = new Headers(options.headers || {});
  if (options.body != null && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  const token = getStoredToken();
  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  let response;
  try {
    response = await fetch(url, { ...options, headers });
  } catch (cause) {
    const error = new Error('No fue posible conectar con SNCEV.Api. Verifica que https://localhost:63730 esté iniciado y que el certificado local sea confiable.');
    error.code = 'NETWORK_ERROR';
    error.cause = cause;
    throw error;
  }

  if (response.status === 204) return null;

const contentType = response.headers.get('content-type') || '';

const isJson =
  contentType.includes('application/json') ||
  contentType.includes('application/geo+json') ||
  contentType.includes('+json');

let data = null;

try {
  data = isJson
    ? await response.json()
    : await response.text();
} catch {
  data = null;
}

  if (!response.ok) {
    const message =
      data?.detail || data?.error || data?.mensaje || data?.message ||
      (response.status === 401
        ? 'Usuario/contraseña incorrectos o la sesión ha expirado.'
        : `Error HTTP ${response.status}.`);
    const error = new Error(typeof message === 'string' ? message : JSON.stringify(message));
    error.status = response.status;
    error.data = data;
    throw error;
  }

  return data;
}

export const apiClient = {
  get: (endpoint, options) => request(endpoint, { ...options, method: 'GET' }),
  post: (endpoint, body, options) => request(endpoint, { ...options, method: 'POST', body: body == null ? undefined : JSON.stringify(body) }),
  patch: (endpoint, body, options) => request(endpoint, { ...options, method: 'PATCH', body: body == null ? undefined : JSON.stringify(body) }),
  put: (endpoint, body, options) => request(endpoint, { ...options, method: 'PUT', body: body == null ? undefined : JSON.stringify(body) }),
  delete: (endpoint, options) => request(endpoint, { ...options, method: 'DELETE' }),
download: async (endpoint) => {
  const url = endpoint.startsWith('http')
    ? endpoint
    : `${API_BASE_URL}${endpoint.startsWith('/') ? '' : '/'}${endpoint}`;

  const headers = new Headers();

  const token = getStoredToken();

  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  let response;

  try {
    response = await fetch(url, {
      method: 'GET',
      headers,
    });
  } catch (cause) {
    const error = new Error(
      'No fue posible conectar con SNCEV.Api.'
    );
    error.code = 'NETWORK_ERROR';
    error.cause = cause;
    throw error;
  }

  if (!response.ok) {
    let mensaje = `Error HTTP ${response.status}.`;

    try {
      const contentType =
        response.headers.get('content-type') || '';

      if (contentType.includes('application/json')) {
        const data = await response.json();

        mensaje =
          data?.detail ||
          data?.error ||
          data?.mensaje ||
          data?.message ||
          mensaje;
      } else {
        const texto = await response.text();

        if (texto) {
          mensaje = texto;
        }
      }
    } catch {
      // Conservamos el mensaje HTTP genérico.
    }

    const error = new Error(mensaje);
    error.status = response.status;
    throw error;
  }

  const blob = await response.blob();

  // Intentamos respetar el nombre enviado por ASP.NET.
  const disposition =
    response.headers.get('content-disposition') || '';

  let filename = 'Cartilla_SNCEV.pdf';

  const utf8Match = disposition.match(
    /filename\*=UTF-8''([^;]+)/i
  );

  const normalMatch = disposition.match(
    /filename="?([^";]+)"?/i
  );

  if (utf8Match?.[1]) {
    try {
      filename = decodeURIComponent(utf8Match[1]);
    } catch {
      filename = utf8Match[1];
    }
  } else if (normalMatch?.[1]) {
    filename = normalMatch[1];
  }

  return {
    blob,
    filename,
  };
},
  auth: {
    login: async (username, password, device_id = null) => {
      clearStoredSession();
      const result = await request('/auth/login', {
        method: 'POST',
        body: JSON.stringify({ username, password, device_id }),
      });

      const accessToken = result?.accessToken || result?.access_token;
      if (!accessToken) throw new Error('SNCEV.Api no devolvió un accessToken.');
      setStoredToken(accessToken);

      try {
        // La identidad nunca se fabrica en React: se valida contra el backend.
        const user = await request('/auth/me', { method: 'GET' });
        setStoredUser(user);
        return { ...result, accessToken, user };
      } catch (error) {
        clearStoredSession();
        throw error;
      }
    },

    me: async () => {
      if (!getStoredToken()) {
        const error = new Error('No existe una sesión activa.');
        error.status = 401;
        throw error;
      }
      try {
        const user = await request('/auth/me', { method: 'GET' });
        setStoredUser(user);
        return user;
      } catch (error) {
        if (error.status === 401 || error.status === 403) clearStoredSession();
        throw error;
      }
    },

    logout: () => clearStoredSession(),
    setToken: setStoredToken,
  },

  app: {
    getPublicSettings: async () => ({
      id: 'sncev-app',
      name: 'SNCEV — Cartilla Electrónica de Vacunación',
      version: '1.0.0',
      public_settings: { allow_registration: false, mfa_enabled: false },
    }),
  },
};

export default apiClient;
