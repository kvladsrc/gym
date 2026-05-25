#include <cassert>
#include <cstdint>
#include <iostream>

static int fibonacci_sum_naive(int64_t n) {
  if (n <= 1) {
    return n;
  }

  int64_t previous = 0;
  int64_t current = 1;
  int64_t sum = 1;

  for (int64_t i = 0; i < n - 1; ++i) {
    int64_t const tmp_previous = previous;
    previous = current;
    current = tmp_previous + current;
    sum += current;
  }

  return sum % 10;
}

static int fibonacci_sum_fast(int64_t n) { return fibonacci_sum_naive(n % 60); }

static void test_solution() {
  for (int n = 0; n < 100; ++n) {
    std::cout << fibonacci_sum_fast(n) << "\n";
    std::cout << fibonacci_sum_naive(n) << "\n";
  }
}

int main() {
  int64_t n = 0;
  test_solution();
  std::cin >> n;
  std::cout << fibonacci_sum_naive(n);
}
