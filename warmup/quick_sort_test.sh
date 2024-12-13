#!/usr/bin/env bats

RUN="bazel run :quick_sort"

@test "empty" {
  result="$(echo '0' | $RUN)"
  [ "$result" = "" ]
}

@test "one element" {
  result="$(echo '1 1' | $RUN)"
  [ "$result" = "1 " ]
}

@test "already sorted" {
  result="$(echo '10 1 2 3 4 5 6 7 8 9 10' | $RUN)"
  [ "$result" = "1 2 3 4 5 6 7 8 9 10 " ]
}

@test "already sorted (reversed)" {
  result="$(echo '10 10 9 8 7 6 5 4 3 2 1' | $RUN)"
  [ "$result" = "1 2 3 4 5 6 7 8 9 10 " ]
}

@test "dubplicates" {
  result="$(echo '10 1 3 3 3 3 3 3 3 4 2' | $RUN)"
  [ "$result" = "1 2 3 3 3 3 3 3 3 4 " ]
}
