import js from '@eslint/js';
import { defineConfig } from 'eslint/config';
import tseslint from 'typescript-eslint';
import react from 'eslint-plugin-react';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';

export default defineConfig(
  {
    ignores: [
      '**/dist/**',
      '**/coverage/**',
      '**/node_modules/**',
      '**/.tsbuild/**',
    ],
  },

  {
    files: ['src/**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      ...tseslint.configs.strictTypeChecked,
      ...tseslint.configs.stylisticTypeChecked,
      react.configs.flat.recommended,
      react.configs.flat['jsx-runtime'],
    ],
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'module',
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    settings: {
      react: { version: 'detect' },
    },
    plugins: {
      react,
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react/prop-types': 'off',
      'react/display-name': 'off',
      'react/no-unescaped-entities': 'off',
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
      '@typescript-eslint/no-unused-vars': ['error', {
        argsIgnorePattern: '^_',
        varsIgnorePattern: '^_',
        caughtErrorsIgnorePattern: '^_',
      }],
      '@typescript-eslint/no-misused-promises': ['error', { checksVoidReturn: { attributes: false } }],
      '@typescript-eslint/no-floating-promises': 'off',
      '@typescript-eslint/no-unnecessary-condition': 'off',
      '@typescript-eslint/prefer-nullish-coalescing': 'off',
      '@typescript-eslint/consistent-type-imports': ['error', { prefer: 'type-imports', fixStyle: 'inline-type-imports' }],
      '@typescript-eslint/no-non-null-assertion': 'off',
      '@typescript-eslint/no-confusing-void-expression': 'off',
      '@typescript-eslint/no-empty-function': 'off',
      '@typescript-eslint/explicit-function-return-type': 'off',
      '@typescript-eslint/explicit-module-boundary-types': 'off',
      '@typescript-eslint/require-await': 'off',
      '@typescript-eslint/no-invalid-void-type': 'off',
      '@typescript-eslint/restrict-template-expressions': 'off',
      '@typescript-eslint/no-unnecessary-type-assertion': 'off',
      '@typescript-eslint/no-unnecessary-type-arguments': 'off',
      '@typescript-eslint/no-unsafe-assignment': 'off',
      '@typescript-eslint/no-unsafe-member-access': 'off',
      '@typescript-eslint/no-unsafe-call': 'off',
      '@typescript-eslint/no-unsafe-return': 'off',
      '@typescript-eslint/no-unsafe-argument': 'off',
      'no-void': 'off',
      'no-console': 'off',
      eqeqeq: ['error', 'always', { null: 'ignore' }],
    },
  },

  {
    // Declaration files frequently require index signature interfaces for
    // global augmentation (e.g. JSX.IntrinsicElements) where Record<K,V>
    // cannot be used as a drop-in replacement.
    files: ['**/*.d.ts'],
    rules: {
      '@typescript-eslint/consistent-indexed-object-style': 'off',
    },
  },


  // ── Convention guardrails (G1 dialog/native + G3 jspdf), synced saas↔community ──
  // Propagated from foundation's Wave-2 flip at `error` (zero violations here).
  // Scope note: the mutation-feedback (meta) selectors are NOT propagated — the
  // product repos have not adopted the meta convention yet. This file is byte-
  // synced saas→community (G4 manifest); per-file exemptions go INLINE in the
  // consuming file (eslint-disable with a reason), never here, so the two
  // configs stay identical. See orkyo-foundation/docs/dialog-feedback.md.
  {
    files: ['src/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', {
        paths: [
          {
            name: 'jspdf',
            message:
              'jspdf is heavy and foundation-owned (dynamic import in gantt-pdf-export). Do not import it in product code. See plan G3.',
          },
          {
            name: '@kymr10n/foundation/src/components/ui/dialog',
            importNames: ['Dialog', 'DialogContent'],
            message:
              'Hand-rolled dialog shell: use FormDialog / ScaffoldDialog / ConfirmDialog. Composition helpers (DialogFooter, DialogHeader, ScrollableDialogBody, …) remain allowed. Genuinely-special dialogs carry an inline eslint-disable with a reason. See foundation docs/dialog-feedback.md (G1).',
          },
        ],
      }],
      'no-restricted-globals': ['error',
        { name: 'alert', message: 'Native alert(): use toast (sonner) or ErrorAlert. See foundation docs/dialog-feedback.md.' },
        { name: 'confirm', message: 'Native confirm(): use ConfirmDialog. See foundation docs/dialog-feedback.md.' },
        { name: 'prompt', message: 'Native prompt(): use a FormDialog. See foundation docs/dialog-feedback.md.' },
      ],
    },
  },
  {
    files: ['**/*.test.{ts,tsx}', '**/*.spec.{ts,tsx}'],
    rules: {
      '@typescript-eslint/no-floating-promises': 'off',
      '@typescript-eslint/no-unsafe-assignment': 'off',
      '@typescript-eslint/no-unsafe-member-access': 'off',
      '@typescript-eslint/no-unsafe-call': 'off',
      '@typescript-eslint/no-unsafe-return': 'off',
      '@typescript-eslint/no-unsafe-argument': 'off',
      '@typescript-eslint/no-unnecessary-condition': 'off',
      'no-console': 'off',
      // Tests may stub dialogs / native prompts freely.
      'no-restricted-imports': 'off',
      'no-restricted-globals': 'off',
    },
  },
);
