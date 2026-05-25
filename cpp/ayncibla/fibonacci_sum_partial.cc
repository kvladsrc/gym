#include <cstdint>
#include <iostream>
#include <vector>
using std::vector;

static int64_t get_fibonacci_partial_sum_naive(int64_t from, int64_t to) {
  int64_t sum = 0;

  int64_t current = 0;
  int64_t next = 1;

  for (int64_t i = 0; i <= to; ++i) {
    if (i >= from) {
      sum += current;
    }

    int64_t const new_current = next;
    next = next + current;
    current = new_current;
  }

  return sum % 10;
}

static int get_fibonacci_partial_sum_fast(int64_t from, int64_t to) {
  from = from % 60;
  to = to % 60;
  if (to < from) {
    to += 60;
  }

  int sum = 0;

  int current = 0;
  int next = 1;

  for (int64_t i = 0; i <= to; ++i) {
    if (i >= from) {
      sum += current;
      sum = sum % 10;
    }

    int const new_current = next;
    next = (next + current) % 10;
    current = new_current;
  }

  return sum;
}

int main() {
  int64_t from = 0;
  int64_t to = 0;
  std::cin >> from >> to;
  std::cout << get_fibonacci_partial_sum_fast(from, to) << '\n';
  std::cout << get_fibonacci_partial_sum_naive(from, to) << '\n';
}
