#!/usr/bin/env bats

RUN="bazel run :prime_test"

@test "negative" {
    input=-5
    expected="NO"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "zero" {
    input=0
    expected="NO"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "one" {
    input=1
    expected="NO"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "small prime (2)" {
    input=2
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "small composite (4)" {
    input=4
    expected="NO"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "another prime (3)" {
    input=3
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "large prime (9973)" {
    input=9973
    expected="YES"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "large composite (10000)" {
    input=10000
    expected="NO"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "square of prime (25)" {
    input=25
    expected="NO"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "even composite (6)" {
    input=6
    expected="NO"
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}
