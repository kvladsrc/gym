#include <cassert>

static int get_fibonacci_last_digit_naive(int n) {
  if (n <= 1) {
    return n;
  }

  int previous = 0;
  int current = 1;

  for (int i = 0; i < n - 1; ++i) {
    int const tmp_previous = previous;
    previous = current;
    current = tmp_previous + current;
  }

  return current % 10;
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

static void test_solution() {
  for (int n = 0; n < 20; ++n) {
    assert(get_fibonacci_last_digit_naive(n) ==
           get_fibonacci_last_digit_fast(n));
  }
}

int main() {
  // int n;
  //     std::cin >> n;
  //     int c = get_fibonacci_last_digit_naive(n);
  //  std::cout << c << '\n';
  test_solution();
}
