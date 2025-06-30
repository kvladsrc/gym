#include <cstddef>
#include <iostream>
#include <string>
#include <utility>
#include <vector>

using std::string;
using std::vector;

static int partition(vector<int> &a, int start, int end) {
  if (start >= end) {
    return start;
  }

  int const pivot = a[end];

  // Index less never used before been incremented.
  int less = start - 1;
  int more = start;

  while (more < end) {
    if (a[more] < pivot) {
      std::swap(a[++less], a[more]);
    }

    more++;
  }
  std::swap(a[++less], a[end]);

  return less;
}

static int nth_element(vector<int> &a, int n, int start, int end) {
  auto pivot = partition(a, start, end);

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

  if (n == 0) {
    std::cout << "EMPTY" << "\n";
  } else {
    std::cout << nth_element(a, (n / 2), 0, n - 1) << "\n";
  }

  return 0;
}
