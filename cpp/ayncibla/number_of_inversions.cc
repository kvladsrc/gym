#include <cstddef>
#include <cstdint>
#include <iostream>
#include <vector>

using std::vector;

static int64_t get_number_of_inversions(vector<int>& a, vector<int>& b,
                                        size_t left, size_t right) {
  int64_t number_of_inversions = 0;
  if (left == right) {
    return number_of_inversions;
  }
  size_t const ave = (left + right) / 2;

  number_of_inversions += get_number_of_inversions(a, b, left, ave);
  number_of_inversions += get_number_of_inversions(a, b, ave + 1, right);

  size_t i = left;
  size_t j = ave + 1;
  size_t cur = 0;

  while (i <= ave || j <= right) {
    if (i > ave) {
      b[cur++] = a[j++];
      continue;
    }

    if (j > right) {
      b[cur++] = a[i++];
      continue;
    }

    if (a[i] <= a[j]) {
      b[cur++] = a[i++];
      continue;
    }

    number_of_inversions += ave + 1 - i;
    b[cur++] = a[j++];
  }

  cur = 0;
  while (left <= right) {
    a[left++] = b[cur++];
  }

  return number_of_inversions;
}

int main() {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (int& i : a) {
    std::cin >> i;
  }
  vector<int> b(a.size());
  std::cout << get_number_of_inversions(a, b, 0, a.size() - 1) << '\n';
}
