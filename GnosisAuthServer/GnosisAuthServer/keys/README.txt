Generate an RSA keypair for JwtOptions before running production.

Example (OpenSSL):
  openssl genrsa -out auth_private.pem 4096
  openssl rsa -in auth_private.pem -pubout -out auth_public.pem

Do not commit real key material to source control.
