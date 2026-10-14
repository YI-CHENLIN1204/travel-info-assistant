# Security

This project integrates multiple external data providers. API credentials and deployment secrets must remain outside Git.

## Never commit

- `.env` files
- API keys or OAuth client secrets
- provider account keys
- private keys / certificates
- service-account credentials
- database dumps containing private data

## Safe repository content

- `.env.example` with placeholders only
- source code
- schemas and documentation
- tests using synthetic / non-sensitive data

If a credential is accidentally committed, rotate it at the provider first, then remove it from the repository history.
