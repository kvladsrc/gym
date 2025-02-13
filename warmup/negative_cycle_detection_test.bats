#!/usr/bin/env bats

RUN="bazel run :negative_cycle_detection"

@test "single vertex no cycle" {
  input=$'1 0'
  expected=$'NO'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "no cycle" {
  input=$'3 2 0 1 3 1 2 4 '
  expected=$'NO'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "negative cycle simple" {
  input=$'3 3 0 1 1 1 2 -3 2 0 1'
  expected=$'YES'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "self loop negative" {
  input=$'1 1 0 0 -1'
  expected=$'YES'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "negative weight but no cycle" {
  input=$'2 1 0 1 -5'
  expected=$'NO'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "disconnected, one component with negative cycle" {
  input=$'4 3 0 1 2 2 3 -1 3 2 -1'
  expected=$'YES'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}
