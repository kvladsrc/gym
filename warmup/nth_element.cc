#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

static void swap(int &a, int &b) noexcept {
  int const buf = a;
  a = b;
  b = buf;
}

static size_t partion(vector<int> &a, int start, int end) {
  int const pivot = a[end];
  int less = start - 1;
  int more = start;
  while (more < end) {
    if (a[more] < pivot) {
      swap(a[++less], a[more]);
    }

    more++;
  }
  swap(a[++less], a[end]);

  return less;
}

static int nth_element(vector<int> &a, size_t n, int start, int end) {
  size_t const pivot = partion(a, start, end);

  if (pivot == n) {
    return a[pivot];
  }

  if (pivot > n) {
    return nth_element(a, n, start, pivot - 1);
  }

  return nth_element(a, n, pivot + 1, end);
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  std::cout << nth_element(a, (n / 2) - 1, 0, n - 1) << "\n";

  return 0;
}
