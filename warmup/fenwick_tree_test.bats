#!/usr/bin/env bats

RUN="bazel run :fenwick_tree"

@test "empty" {
  input=$'0'
  expected=$''
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "one element" {
  input=$'1 1 1 1 1'
  expected=$'1\n1'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "simple" {
  input=$'10 7 6 45 32 6 89 67 4 6 8 5 1 1 1 2 2 4 8 9 4 7'
  expected=$'7\n7\n7\n6\n45\n6\n6\n4\n89\n6'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "all equal elements" {
  input=$'5 2 2 2 2 2 2 1 3 2 5'
  expected=$'2\n2\n2\n2'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "full range" {
  input=$'5 5 3 8 1 9 1 1 5'
  expected=$'9\n1'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}
