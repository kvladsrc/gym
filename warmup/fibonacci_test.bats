#!/usr/bin/env bats

RUN="bazel run :fibonacci"

@test "zero" {
    input=$'0'
    expected=$'0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one" {
    input=$'1'
    expected=$'1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two" {
    input=$'2'
    expected=$'1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "small number" {
    input=$'10'
    expected=$'55'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "medium number" {
    input=$'20'
    expected=$'6765'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "large number" {
    input=$'50'
    expected=$'12586269025'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "largest 64-bit safe number" {
    input=$'92'
    expected=$'7540113804746346429'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "maximum single-digit input" {
    input=$'9'
    expected=$'34'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "negative" {
    input=$'-1'
    expected=$'MUST BE POSITIVE'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
