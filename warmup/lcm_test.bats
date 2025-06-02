#!/usr/bin/env bats

RUN="bazel run :lcm"

@test "LCM of 4 and 6 is 12" {
    input=$'4 6'
    expected=$'12'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "LCM with one zero (0 5)" {
    input=$'0 5'
    expected=$'0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "LCM with one zero (5 0)" {
    input=$'5 0'
    expected=$'0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "LCM with both zeros (0 0)" {
    input=$'0 0'
    expected=$'0'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "LCM with negative first number (-4 6)" {
    input=$'-4 6'
    expected=$'12'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "LCM with both negatives (-4 -6)" {
    input=$'-4 -6'
    expected=$'12'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "LCM of co-prime numbers (7 3)" {
    input=$'7 3'
    expected=$'21'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "LCM of equal numbers (10 10)" {
    input=$'10 10'
    expected=$'10'
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
