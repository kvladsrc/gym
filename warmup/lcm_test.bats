#!/usr/bin/env bats

RUN="bazel run :lcm"

# TODO(zjgkkn): rewrite according the format with input/expect. Add
# more tests.
@test "first" {
    result="$(echo '10 20' | $RUN)"
    [ "$result" = "20" ]
}

@test "second" {
    result="$(echo '3 5' | $RUN)"
    [ "$result" = "15" ]
}

@test "third" {
    result="$(echo '12 30' | $RUN)"
    [ "$result" = "60" ]
}
