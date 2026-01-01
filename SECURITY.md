# Security Policy

## Supported Versions

We release patches for security vulnerabilities. Which versions are eligible for receiving such patches depends on the CVSS v3.0 Rating:

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

We take the security of Delgato seriously. If you believe you have found a security vulnerability, please report it to us as described below.

### Please do NOT:

- Open a public GitHub issue
- Post about it on social media
- Disclose it publicly before we have had a chance to address it

### Please DO:

1. **Report via GitHub Security Advisories**: Go to the [Security Advisories](https://github.com/JerrettDavis/Delgato/security/advisories/new) page and create a new private security advisory.

2. **Open a GitHub Issue** (for non-sensitive issues): If the vulnerability is not sensitive, you can also open a regular issue with the `security` label.

### What to expect:

- **Acknowledgment**: We will acknowledge your report within 48 hours
- **Updates**: We will keep you informed about the progress
- **Resolution**: We aim to resolve critical issues within 7 days
- **Credit**: We will credit you in our security advisory (unless you prefer to remain anonymous)

## Security Best Practices for Users

### API Key Management

- Never commit API keys to version control
- Use environment variables or secure secret management
- Rotate API keys regularly
- Use separate keys for development and production

### Deployment

- Always use HTTPS in production
- Keep all dependencies updated
- Enable audit logging
- Set appropriate budget limits
- Configure compliance policies

### Agent Configuration

- Use the principle of least privilege for agent permissions
- Deny dangerous tools in production policies
- Enable audit logging for all tool calls
- Set strict budget limits

### Example Secure Policy

```yaml
policy:
  deniedTools:
    - delete_file
    - execute_shell
    - write_file
  requiredCapabilities:
    - safe-execution
  auditAllToolCalls: true
  maxCostPerRequest: 1.0
```

## Security Features

Delgato includes several security features:

- **Audit Logging**: Complete audit trail of all operations
- **Cost Tracking**: Budget enforcement to prevent runaway costs
- **Policy Enforcement**: Compliance validation before execution
- **Tool Restrictions**: Deny list for dangerous operations
- **API Key Isolation**: Per-provider key management

## Vulnerability Disclosure Timeline

1. **Day 0**: Vulnerability reported
2. **Day 2**: Initial acknowledgment
3. **Day 7**: Assessment and triage completed
4. **Day 14**: Fix developed and tested
5. **Day 21**: Security advisory published
6. **Day 28**: Public disclosure (if applicable)

## Recognition

We appreciate the security research community's efforts in helping keep Delgato secure. Contributors who responsibly disclose vulnerabilities will be:

- Acknowledged in our security advisories
- Added to our Security Hall of Fame (with permission)
- Eligible for our bug bounty program (when available)

## Contact

- GitHub Security Advisories: [Create Advisory](https://github.com/JerrettDavis/Delgato/security/advisories/new)
- GitHub Issues: [Open Issue](https://github.com/JerrettDavis/Delgato/issues) (for non-sensitive issues)
