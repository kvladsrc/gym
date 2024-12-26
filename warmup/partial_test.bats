#!/usr/bin/env bats

RUN="bazel run :partial"

@test "increasing" {
  result="$(echo '10 1 2 3 4 5 6 7 8 9 10' | $RUN)"
  [ "$result" = "9
1 2 3 4 5 6 7 8 9 10 " ]
}

@test "decreasing" {
  result="$(echo '10 10 9 8 7 6 5 4 3 2 1' | $RUN)"
  [ "$result" = "0
1 9 8 7 6 5 4 3 2 10 " ]
}

@test "mixed" {
  result="$(echo '20 7 5 3 8 6 4 9 6 4 3 3 4 5 6 9 8 7 6 5 6' | $RUN)"
  [ "$result" = "9
5 5 3 5 4 4 3 3 4 6 9 6 8 6 9 8 7 6 7 6 " ]
}

@test "empty" {
  result="$(echo '0' | $RUN)"
  [ "$result" = "-1" ]
}
