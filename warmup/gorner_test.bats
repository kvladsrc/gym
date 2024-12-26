#!/usr/bin/env bats

RUN="bazel run :gorner"

@test "zero" {
  result="$(echo '0 1' | $RUN)"
  [ "$result" = "0" ]
}

@test "x is one" {
  result="$(echo '10 10 9 8 7 6 5 4 3 2 1 1' | $RUN)"
  [ "$result" = "55" ]
}

@test "x is ten" {
  result="$(echo '9 1 2 3 4 5 6 7 8 9 10' | $RUN)"
  [ "$result" = "123456789" ]
}

@test "zeroe coefs" {
  result="$(echo '10 1 0 0 0 0 0 0 0 0 1 10' | $RUN)"
  [ "$result" = "1000000001" ]
}
