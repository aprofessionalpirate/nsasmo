# Scoped CLI user

Use the existing administrator console session for this one-time setup. The
deployment user cannot create users or edit its own managed policies.

## Create two policies

Find the 12-digit AWS account ID in the console's account menu. Replace every
`ACCOUNT_ID` in both JSON files with that ID before pasting them into IAM. The
policies are for Sydney (`ap-southeast-2`) and stack name `spideynet-ecs`.

1. IAM -> Policies -> Create policy -> JSON.
2. Paste `execution-boundary.json`; name it `SpideyNetEcsExecutionBoundary`.
3. Create another policy from `deployer-policy.json`; name it
   `SpideyNetEcsDeployer`.

The first policy caps the role that Fargate uses to pull images and write logs.
Do not attach it as the deployment user's permissions boundary: it would prevent
deployment. The template attaches it to the execution role automatically.

The deployer policy scopes stack, registries, ECS cluster/service/task family,
logs and execution role to this trial. It only permits creation of the execution
role when the boundary is present, and only permits passing it to ECS tasks.
It cannot modify or remove that boundary or edit/create managed IAM policies.

Some AWS APIs need broader resources: networking reads, new security-group
creation, task-definition description/deregistration, template validation, and registry login.
AWS does not support resource restrictions for `DescribeTaskDefinition` or
`DeregisterTaskDefinition`; these permissions use `Resource: "*"` with a Sydney
region condition. This permits deregistering other task definitions in Sydney.
Permanent deletion remains restricted to the trial task family.
Regional APIs are limited to Sydney. The user can create security groups and
register task definitions in that region; it cannot launch EC2 instances or run
arbitrary ECS tasks. Existing firewall mutations require this stack's system tag.
It can change the trial service's task count, so this policy is not a spending cap.

The policies passed a live stack deployment and image upload. The newer
CloudFormation `DescribeEvents` diagnostic API is not granted; basic stack,
resource and ECS service status checks remain available. If AWS reports
AccessDenied, inspect the exact action/resource rather than attaching
AdministratorAccess.

## Create the user

1. IAM -> Users -> Create user; name it `spideynet-deployer`.
2. Enable AWS Management Console access, choosing an IAM user if prompted.
   This is needed for temporary browser-based CLI login, not for managing the
   server through SSH.
3. Attach policies directly: `SpideyNetEcsDeployer` and AWS-managed
   `SignInLocalDevelopmentAccess`.
4. Create the user and save its console sign-in link. Set up MFA under the
   user's Security credentials from the existing administrator session.
5. Sign into that IAM user in a separate/private browser window.

Do not create access keys. With AWS CLI 2.32+ installed, run this locally:

```powershell
aws login --profile spideynet --region ap-southeast-2
aws sts get-caller-identity --profile spideynet --region ap-southeast-2
```

Choose the `spideynet-deployer` console session, not root or the administrator.
The identity ARN should end in `user/spideynet-deployer`. A dedicated CLI profile
avoids the old invalid default credentials. Keep login codes and credentials
out of chat.

AWS recommends federation for human access; IAM Identity Center is an alternative
if already configured. This setup uses the explicitly requested IAM user and
temporary CLI sessions rather than persistent access keys.
