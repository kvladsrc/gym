#!/usr/bin/env bats

RUN="bazel run :boyer_moore"

@test "empty pattern and empty string" {
    input=$'0 0'
    expected=$'FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "empty pattern and non-empty string" {
    input=$'0 3\nabc'
    expected=$'FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "non-empty pattern and empty string" {
    input=$'3\nabc\n0\n'
    expected=$'NOT FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "pattern equals string" {
    input=$'3\nabc\n3\nabc'
    expected=$'FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "pattern at the beginning" {
    input=$'2\nab\n6\nabcdef'
    expected=$'FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "pattern in the middle" {
    input=$'2\ncd\n6\nabcdef'
    expected=$'FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "pattern at the end" {
    input=$'2\nef\n6\nabcdef'
    expected=$'FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "pattern not found" {
    input=$'3\nxyz\n6\nabcdef'
    expected=$'NOT FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "partial match" {
    input=$'3\nabcd\n6\nabdabc'
    expected=$'NOT FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "repeating characters found" {
    input=$'3\naaa\n6\naabaaa'
    expected=$'FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "repeating characters not found" {
    input=$'4\naaaa\n4\naaab'
    expected=$'NOT FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "pattern longer than string" {
    input=$'3\nabc\n2\nab'
    expected=$'NOT FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single character found" {
    input=$'1\na\n3\nabc'
    expected=$'FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single character not found" {
    input=$'1\nd\n3\nabc'
    expected=$'NOT FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "multiple occurrences" {
    input=$'2\nab\n6\nabcdab'
    expected=$'FOUND'
    result="$(echo -e "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
