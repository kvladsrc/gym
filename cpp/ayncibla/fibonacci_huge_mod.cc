#include <cstdint>
#include <iostream>

static int64_t get_fibonacci_huge_fast_compute(int64_t n, int64_t m) {
  if (n <= 1) {
    return n;
  }

  int64_t previous = 0;
  int64_t current = 1;

  for (int64_t i = 0; i < n - 1; ++i) {
    int64_t const tmp_previous = previous;
    previous = current;
    current = (tmp_previous + current) % m;
  }

  return current;
}

static int64_t get_period_length(int64_t /*n*/, int64_t m) {
  bool zero = false;
  int64_t cur = 2;

  while (true) {
    int64_t const cur_fib = get_fibonacci_huge_fast_compute(cur, m);
    if (zero && cur_fib == 1) {
      break;
    }

    zero = cur_fib == 0;

    cur++;
  }

  return cur - 1;
}

static int64_t get_fibonacci_huge_fast(int64_t n, int64_t m) {
  if (n <= 1) {
    return n;
  }

  int64_t const period_length = get_period_length(n, m);
  return get_fibonacci_huge_fast_compute(n % period_length, m);
}

int main() {
  int64_t n = 0;
  int64_t m = 0;
  std::cin >> n >> m;
  std::cout << get_fibonacci_huge_fast(n, m) << '\n';
}
