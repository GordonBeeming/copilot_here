#!/usr/bin/env bash
# Post or update a "sticky" PR comment, identified by an HTML marker comment
# in the first line of the body. Avoids spamming a long-running PR with a new
# comment on every push.
#
# Usage: sticky-pr-comment.sh <owner/repo> <pr-number> <body-file>
#
# The body file's FIRST line MUST be the HTML marker (e.g.
#   <!-- regen-dockerfiles-bot -->
# ). Subsequent runs find the prior comment by exact-prefix match on that
# marker and PATCH it in place; if none exists yet, a new comment is created.
#
# Requires: gh (authenticated, with pull-requests:write), jq.

set -euo pipefail

repo=$1
pr=$2
body_file=$3

if [ ! -s "$body_file" ]; then
  echo "sticky-pr-comment: body file '$body_file' is empty or missing" >&2
  exit 1
fi

marker=$(head -n 1 "$body_file")
case "$marker" in
  '<!--'*'-->') ;;
  *)
    echo "sticky-pr-comment: first line of body file must be an HTML marker comment, got: $marker" >&2
    exit 1
    ;;
esac

existing_id=$(gh api "repos/${repo}/issues/${pr}/comments" --paginate \
  --jq ".[] | select(.body | startswith(\"${marker}\")) | .id" \
  | head -n 1)

if [ -n "$existing_id" ]; then
  echo "sticky-pr-comment: updating existing comment $existing_id"
  jq -n --rawfile body "$body_file" '{body: $body}' \
    | gh api -X PATCH "repos/${repo}/issues/comments/${existing_id}" --input -
else
  echo "sticky-pr-comment: creating new comment"
  gh pr comment "$pr" --repo "$repo" --body-file "$body_file"
fi
