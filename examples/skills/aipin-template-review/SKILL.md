# AI Quick Prompt Template Review Skill

## Description

Use this Skill when a user wants to review, improve, or normalize a prompt template for AI Quick Prompt. The goal is to make the template easier to reuse without changing the user's intent.

## Trigger Examples

- review this prompt template
- make this template cleaner
- turn this into a reusable AI Quick Prompt template
- normalize this prompt for the template library

## Workflow

1. Identify the intended template category.
2. Preserve the original use case and audience.
3. Remove duplicate wording and unclear constraints.
4. Keep placeholders explicit with `{placeholder_name}`.
5. Separate reusable template content from usage notes.
6. Return a concise final template plus a short list of remaining missing fields.

## Output Format

```text
Template title:

Category:

Template:

Placeholders:

Missing fields:
```
