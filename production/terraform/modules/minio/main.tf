locals {
  minio_buckets = {
    registry = {
      actions = ["s3:*"]
    }
    buildbuddy = {
      actions = ["s3:*"]
    }
    zuul = {
      actions = ["s3:*"]
    }
  }
}

resource "minio_s3_bucket" "this" {
  for_each = local.minio_buckets
  bucket   = each.key
  acl      = "private"
}

resource "minio_iam_user" "this" {
  for_each      = local.minio_buckets
  name          = each.key
  force_destroy = true
}

resource "minio_iam_policy" "this" {
  for_each = local.minio_buckets
  name     = "${each.key}-policy"
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = each.value.actions
        Resource = [
          "arn:aws:s3:::${each.key}",
          "arn:aws:s3:::${each.key}/*",
        ]
      }
    ]
  })
}

resource "minio_iam_user_policy_attachment" "this" {
  for_each    = local.minio_buckets
  user_name   = minio_iam_user.this[each.key].name
  policy_name = minio_iam_policy.this[each.key].name
}

resource "minio_iam_service_account" "this" {
  for_each    = local.minio_buckets
  target_user = minio_iam_user.this[each.key].name
  depends_on  = [minio_iam_user_policy_attachment.this]
}

output "minio_access_keys" {
  value = {
    for k, v in minio_iam_service_account.this : k => v.access_key
  }
}

output "minio_secret_keys" {
  value = {
    for k, v in minio_iam_service_account.this : k => v.secret_key
  }
  sensitive = true
}
