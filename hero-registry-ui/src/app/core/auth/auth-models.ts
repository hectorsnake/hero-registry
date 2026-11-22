/** The signed-in user (`UserResponse` in the API). */
export interface User {
  id: number;
  userName: string;
  email: string;
  isAdmin: boolean;
  createdAt: string;
}

/** Body of `POST /api/auth/login` and `/refresh`. The refresh token is an HttpOnly cookie, never here. */
export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  tokenType: 'Bearer';
  user: User;
}

export interface RegisterRequest {
  userName: string;
  email: string;
  password: string;
}
