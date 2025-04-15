#!/usr/bin/env bats

RUN="bazel run :fast_power"

@test "small exponent" {
    input='2 3'
    expected='8'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "exponent zero" {
    input='5 0'
    expected='1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "base zero" {
    input='0 5'
    expected='0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "large exponent" {
    input='2 30'
    expected='73741817'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "large base" {
    input='123456 2'
    expected='241383831'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "modulo operation check" {
    input='1000000006 2'
    expected='1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one power any exponent" {
    input='1 100000'
    expected='1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "maximum modulo check" {
    input='2 1000000000'
    expected='140625001'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "identity modulo check" {
    input='1000000007 5'
    expected='0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "power of zero to zero" {
    input='0 0'
    expected='1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
