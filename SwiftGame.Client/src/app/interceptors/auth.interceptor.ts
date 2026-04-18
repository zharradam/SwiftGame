import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  const auth = inject(AuthService);
  const token = auth.getAccessToken();

  // Skip auth header for auth endpoints themselves
  const isAuthEndpoint = req.url.includes('/api/auth/');
  const authedReq = token && !isAuthEndpoint ? addToken(req, token) : req;

  return next(authedReq).pipe(
    catchError((err: HttpErrorResponse) => {
      // If 401 and we have a refresh token, try to silently refresh once
      if (err.status === 401 && !isAuthEndpoint) {
        return auth.refresh().pipe(
          switchMap(res => {
            if (res?.accessToken) {
              return next(addToken(req, res.accessToken));
            }
            auth.logout();
            return throwError(() => err);
          })
        );
      }
      return throwError(() => err);
    })
  );
};

function addToken(req: HttpRequest<unknown>, token: string) {
  return req.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
  });
}