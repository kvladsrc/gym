#!/usr/bin/env bats

RUN="bazel run :lcs"

@test "empty sequences" {
    input=$'0 0'
    expected=$'0\n0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "first non-empty, second empty" {
    # n = 3, a = [1,2,3], m = 0
    input=$'3 1 2 3 0'
    expected=$'0\n0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "no common elements" {
    # n = 3, a = [1,2,3], m = 3, b = [4,5,6]
    input=$'3 1 2 3 3 4 5 6'
    expected=$'0\n0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "complete match" {
    # n = 4, a = [1,2,3,4], m = 4, b = [1,2,3,4]
    input=$'4 1 2 3 4 4 1 2 3 4'
    expected=$'4\n4'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "partial match" {
    # n = 3, a = [1,2,3], m = 3, b = [2,1,3]
    # Один из возможных LCS: [1,3] или [2,3] => длина 2.
    input=$'3 1 2 3 3 2 1 3'
    expected=$'2\n2'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "repeated elements" {
    # n = 5, a = [1,2,3,2,1], m = 7, b = [1,2,1,3,1,2,1]
    # Один из возможных вариантов общего подпоследовательности: [1,2,3,2,1] (длина 5).
    input=$'5\n1 2 3 2 1\n7\n1 2 1 3 1 2 1'
    expected=$'5\n5'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
