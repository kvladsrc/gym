#!/usr/bin/env bats

RUN="bazel run :gcd"

@test "gcd of 4 and 6 is 2" {
    input=$'4 6'
    expected=$'2'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "gcd with one zero (0 5)" {
    input=$'0 5'
    expected=$'5'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "gcd with one zero (5 0)" {
    input=$'5 0'
    expected=$'5'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "gcd with both zeros (0 0)" {
    input=$'0 0'
    expected=$'0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "gcd with negative first number (-4 6)" {
    input=$'-4 6'
    expected=$'2'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "gcd with both negatives (-4 -6)" {
    input=$'-4 -6'
    expected=$'2'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "gcd of co-prime numbers (7 3)" {
    input=$'7 3'
    expected=$'1'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "gcd of equal numbers (10 10)" {
    input=$'10 10'
    expected=$'10'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
