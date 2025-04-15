#!/usr/bin/env bats

RUN="bazel run :erato"

@test "primes up to 10" {
    input='10'
    expected='2 3 5 7 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "primes up to 2 (minimum prime)" {
    input='2'
    expected='2 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "no primes (n less than 2)" {
    input='1'
    expected=''
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "primes up to 20" {
    input='20'
    expected='2 3 5 7 11 13 17 19 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "primes up to 30" {
    input='30'
    expected='2 3 5 7 11 13 17 19 23 29 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "larger range: primes up to 50" {
    input='50'
    expected='2 3 5 7 11 13 17 19 23 29 31 37 41 43 47 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "no primes (0 input)" {
    input='0'
    expected=''
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "prime number itself as upper bound (13)" {
    input='13'
    expected='2 3 5 7 11 13 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "prime number itself as upper bound (17)" {
    input='17'
    expected='2 3 5 7 11 13 17 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
