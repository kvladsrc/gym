#!/usr/bin/env bats

RUN="bazel run :jenkins_hash"

@test "one" {
  result="$(echo 'The quick brown fox jumps over the lazy dog' | $RUN)"
  [ "$result" = "0x519e91f5" ]
}

@test "a" {
  result="$(echo 'a' | $RUN)"
  [ "$result" = "0xca2e9442" ]
}
