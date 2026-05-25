#include <cstdint>
#include <iostream>

static int fibonacci_sum_squares_naive(int64_t n) {
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
    sum += current * current;
  }

  return sum % 10;
}

static int get_fibonacci_last_digit_fast(int n) {
  if (n <= 1) {
    return n;
  }

  int previous = 0;
  int current = 1;

  for (int i = 0; i < n - 1; ++i) {
    int const tmp_previous = previous;
    previous = current;
    current = (tmp_previous + current) % 10;
  }

  return current;
}

static int fibonacci_sum_squares_fast(int64_t n) {
  int const height = get_fibonacci_last_digit_fast(n % 60);
  int const width = get_fibonacci_last_digit_fast((n - 1) % 60);
  return (height * width) % 10;
}

int main() {
  int64_t n = 0;
  std::cin >> n;
  std::cout << fibonacci_sum_squares_fast(n);
  std::cout << fibonacci_sum_squares_naive(n);
}
