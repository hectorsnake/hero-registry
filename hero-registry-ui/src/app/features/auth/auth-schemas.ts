import {
  ValidationError,
  apply,
  email,
  maxLength,
  minLength,
  pattern,
  required,
  schema,
  validate,
  validateTree,
} from '@angular/forms/signals';

/** Every message the auth forms show, in one place (templates never hard-code validation text). */
export const AUTH_MESSAGES = {
  required: 'This field is required.',
  invalid: 'This value is not valid.',
  userNameLength: 'Use 3 to 50 characters.',
  userNamePattern: 'Use only letters, digits, dots, underscores and hyphens.',
  email: 'Enter a valid email address.',
  passwordLength: 'Use 8 to 64 characters.',
  passwordMismatch: 'The passwords do not match.',
  invalidCredentials: 'Invalid user name or password.',
  sessionExpired: 'Your session has expired. Please sign in again.',
} as const;

/** Default messages by error kind, for errors that carry no message of their own. */
const MESSAGES_BY_KIND: Readonly<Record<string, string>> = {
  required: AUTH_MESSAGES.required,
  email: AUTH_MESSAGES.email,
  'password.mismatch': AUTH_MESSAGES.passwordMismatch,
};

/** The text to show for a validation error: its own message, else the map's, else a generic one. */
export function errorMessage(error: ValidationError): string {
  return error.message ?? MESSAGES_BY_KIND[error.kind] ?? AUTH_MESSAGES.invalid;
}

export interface LoginModel {
  userName: string;
  password: string;
}

export interface RegisterModel {
  userName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

/** Mirrors the server's UserRules: 3 to 50 characters, letters, digits, `.`, `_` and `-`. */
export const userNameSchema = schema<string>((path) => {
  required(path, { message: AUTH_MESSAGES.required });
  minLength(path, 3, { message: AUTH_MESSAGES.userNameLength });
  maxLength(path, 50, { message: AUTH_MESSAGES.userNameLength });
  pattern(path, /^[a-zA-Z0-9._-]+$/, { message: AUTH_MESSAGES.userNamePattern });
});

export const emailSchema = schema<string>((path) => {
  required(path, { message: AUTH_MESSAGES.required });
  email(path, { message: AUTH_MESSAGES.email });
});

export type PasswordRuleId = 'length' | 'uppercase' | 'lowercase' | 'digit' | 'symbol';

/** A `password.policy` error names the rule it breaks, so the UI can render a checklist. */
export interface PasswordPolicyError extends ValidationError {
  readonly kind: 'password.policy';
  readonly rule: PasswordRuleId;
}

/** The character rules, Unicode-aware like the server (`char.IsUpper`, `IsLower`, `IsDigit`). */
const CHARACTER_RULES: readonly { id: PasswordRuleId; test: RegExp; message: string }[] = [
  { id: 'uppercase', test: /\p{Lu}/u, message: 'Add an uppercase letter.' },
  { id: 'lowercase', test: /\p{Ll}/u, message: 'Add a lowercase letter.' },
  { id: 'digit', test: /\p{Nd}/u, message: 'Add a digit.' },
  { id: 'symbol', test: /[^\p{L}\p{Nd}]/u, message: 'Add a symbol, such as ! or -.' },
];

const CHECKLIST_LABELS: Readonly<Record<PasswordRuleId, string>> = {
  length: '8 to 64 characters',
  uppercase: 'An uppercase letter',
  lowercase: 'A lowercase letter',
  digit: 'A digit',
  symbol: 'A symbol',
};

/** The server's password policy: 8 to 64 characters with an uppercase, a lowercase, a digit and a symbol. */
export const passwordPolicySchema = schema<string>((path) => {
  required(path, { message: AUTH_MESSAGES.required });
  minLength(path, 8, { message: AUTH_MESSAGES.passwordLength });
  maxLength(path, 64, { message: AUTH_MESSAGES.passwordLength });
  validate(path, ({ value }) => {
    const password = value();
    if (!password) {
      return undefined; // `required` already reports it
    }

    return CHARACTER_RULES.filter((rule) => !rule.test.test(password)).map(
      (rule): PasswordPolicyError => ({
        kind: 'password.policy',
        rule: rule.id,
        message: rule.message,
      }),
    );
  });
});

/** Registration: every field rule, plus confirmPassword must repeat password (error on confirmPassword). */
export const registerSchema = schema<RegisterModel>((path) => {
  apply(path.userName, userNameSchema);
  apply(path.email, emailSchema);
  apply(path.password, passwordPolicySchema);
  required(path.confirmPassword, { message: AUTH_MESSAGES.required });
  validateTree(path, ({ value, fieldTree }) => {
    const { password, confirmPassword } = value();
    return confirmPassword && password !== confirmPassword
      ? {
          kind: 'password.mismatch',
          message: AUTH_MESSAGES.passwordMismatch,
          fieldTree: fieldTree.confirmPassword,
        }
      : undefined;
  });
});

/** Login only needs both fields: the policy applies to new passwords, not existing ones (US-001). */
export const loginSchema = schema<LoginModel>((path) => {
  required(path.userName, { message: AUTH_MESSAGES.required });
  required(path.password, { message: AUTH_MESSAGES.required });
});

export interface ChecklistItem {
  id: PasswordRuleId;
  label: string;
  met: boolean;
}

/** The live password checklist, derived from the password field's current errors. */
export function passwordChecklist(errors: readonly ValidationError[]): ChecklistItem[] {
  const empty = errors.some((e) => e.kind === 'required');
  const broken = new Set<PasswordRuleId>(
    errors.flatMap((e): PasswordRuleId[] => {
      if (e.kind === 'minLength' || e.kind === 'maxLength') {
        return ['length'];
      }

      return e.kind === 'password.policy' ? [(e as PasswordPolicyError).rule] : [];
    }),
  );

  return (Object.keys(CHECKLIST_LABELS) as PasswordRuleId[]).map((id) => ({
    id,
    label: CHECKLIST_LABELS[id],
    met: !empty && !broken.has(id),
  }));
}
