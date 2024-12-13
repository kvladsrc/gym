#!/usr/bin/env bats

RUN="bazel run :median_of_stream"

@test "empty" {
  result="$(echo '0' | $RUN)"
  [ "$result" = "0" ]
}

@test "increasing even" {
  result="$(echo '10 1 2 3 4 5 6 7 8 9 10' | $RUN)"
  [ "$result" = "5" ]
}

@test "increasing odd" {
  result="$(echo '11 1 2 3 4 5 6 7 8 9 10 11' | $RUN)"
  [ "$result" = "6" ]
}

@test "decreasing even" {
  result="$(echo '10 10 9 8 7 6 5 4 3 2 1' | $RUN)"
  [ "$result" = "5" ]
}

@test "decreasing odd" {
  result="$(echo '11 11 10 9 8 7 6 5 4 3 2 1' | $RUN)"
  [ "$result" = "6" ]
}

@test "duplicates" {
  result="$(echo '10 1 2 2 2 2 2 2 2 2 3' | $RUN)"
  [ "$result" = "2" ]
}

@test "lower bound" {
  result="$(echo '10 1 1 1 1 1 2 2 2 2 2' | $RUN)"
  [ "$result" = "1" ]
}

@test "upper bound" {
  result="$(echo '10 1 1 1 1 2 2 2 2 2 2' | $RUN)"
  [ "$result" = "2" ]
}

@test "mixed" {
  result="$(echo '10 1 2 2 2 1 1 2 2 1 1' | $RUN)"
  [ "$result" = "1" ]
}
