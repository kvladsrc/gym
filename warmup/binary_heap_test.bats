#!/usr/bin/env bats

RUN="bazel run :binary_heap"

@test "empty" {
    input=$'0'
    expected=$''
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element" {
    input=$'1 1'
    expected=$'1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "already sorted" {
    input=$'10 1 2 3 4 5 6 7 8 9 10'
    expected=$'1\n2\n3\n4\n5\n6\n7\n8\n9\n10'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "already sorted (reversed)" {
    input=$'10 10 9 8 7 6 5 4 3 2 1'
    expected=$'1\n2\n3\n4\n5\n6\n7\n8\n9\n10'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "duplicates" {
    input=$'10 1 3 3 3 3 3 3 3 4 2'
    expected=$'1\n2\n3\n3\n3\n3\n3\n3\n3\n4'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "all equal elements" {
    input=$'5 1 1 1 1 1'
    expected=$'1\n1\n1\n1\n1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "random order" {
    input=$'6 4 1 3 2 6 5'
    expected=$'1\n2\n3\n4\n5\n6'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "negative numbers" {
    input=$'6 -3 -1 -2 -4 0 5'
    expected=$'-4\n-3\n-2\n-1\n0\n5'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one element negative" {
    input=$'1 -1'
    expected=$'-1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
