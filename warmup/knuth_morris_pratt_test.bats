#!/usr/bin/env bats

RUN="bazel run :knuth_morris_pratt"

# TODO(zjgkkn): rewrite according the format with input/expect. Add
# more tests.
@test "found substring" {
    result="$(echo 'abc aabcc' | $RUN)"
    [ "$result" = "FOUND" ]
}

@test "not found substring" {
    result="$(echo 'abcd aabcc' | $RUN)"
    [ "$result" = "NOT FOUND" ]
}

@test "patt = s" {
    result="$(echo 'abcd abcd' | $RUN)"
    [ "$result" = "FOUND" ]
}

@test "found at the begining" {
    result="$(echo 'abcd abcdasdasda' | $RUN)"
    [ "$result" = "FOUND" ]
}

@test "found at the end" {
    result="$(echo 'abcd vcxvxcvxcvabcd' | $RUN)"
    [ "$result" = "FOUND" ]
}

@test "not found" {
    result="$(echo 'abcd aaabbbcccdd' | $RUN)"
    [ "$result" = "NOT FOUND" ]
}
