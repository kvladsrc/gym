#!/usr/bin/env bats

RUN="bazel run :knapsack"

@test "empty array" {
  input=$'0 10'
  expected=$'0'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "one item fits" {
  input=$'1 1 1 1'
  expected=$'1'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "one item does not fit" {
  input=$'1 0 1 1'
  expected=$'0'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "two items, choose best" {
  input=$'2 10 5 7 10 13'
  expected=$'13'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "combination" {
  input=$'3 10 5 4 6 10 40 30'
  expected=$'70'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "capacity zero" {
  input=$'3 0 2 3 4 5 6 7'
  expected=$'0'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "all items too heavy" {
  input=$'3 5 6 7 8 10 20 30'
  expected=$'0'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "duplicate items" {
  input=$'4 5 2 2 2 2 1 2 3 4'
  expected=$'7'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "exact fit" {
  input=$'3 11 5 5 6 10 10 15'
  expected=$'25'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}
