#!/usr/bin/env bats

RUN="bazel run :gorner"

@test "Constant polynomial: p(x)=42" {
  input=$'1 42 100'
  expected=$'42'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "Linear polynomial: p(x)=1*x+2" {
  input=$'2 1 2 5'
  expected=$'7'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "Quadratic polynomial: p(x)=2*x^2+3*x+4 with x=2" {
  input=$'3 2 3 4 2'
  expected=$'18'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "Polynomial at x=0: p(x)=9*x^2+8*x+7" {
  input=$'3 9 8 7 0'
  expected=$'7'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "Polynomial with negative coefficients: p(x)=-1*x^2+2*x-3 with x=3" {
  input=$'3 -1 2 -3 3'
  expected=$'-6'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "Polynomial with negative x: p(x)=1*x^2+2*x+3 with x=-2" {
  input=$'3 1 2 3 -2'
  expected=$'3'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}

@test "Zero polynomial" {
  input=$'3 0 0 0 42'
  expected=$'0'
  result="$(echo "$input" | $RUN)"
  [ "$result" = "$expected" ]
}
