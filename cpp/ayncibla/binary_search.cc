#include <cassert>
#include <cstddef>
#include <iostream>
#include <vector>

using std::vector;

static int binary_search(const vector<int>& a, int x) {
  int left = 0;
  int right = static_cast<int>(a.size() - 1);
  int mid = right / 2;
  while (left <= right) {
    if (x == a[mid]) {
      return mid;
    }

    if (x > a[mid]) {
      left = mid + 1;
    }

    if (x < a[mid]) {
      right = mid - 1;
    }

    mid = (left + right) / 2;
  }

  return -1;
}

#include <algorithm>

static int linear_search(const vector<int>& a, int x) {
  auto it = std::find(a.begin(), a.end(), x);
  if (it != a.end()) {
    return std::distance(a.begin(), it);
  }
  return -1;
}

int main() {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (int& i : a) {
    std::cin >> i;
  }
  int m = 0;
  std::cin >> m;
  vector<int> b(m);
  for (int i = 0; i < m; ++i) {
    std::cin >> b[i];
  }
  for (int i = 0; i < m; ++i) {
    // replace with the call to binary_search when implemented
    std::cout << linear_search(a, b[i]) << "\n";
    std::cout << binary_search(a, b[i]) << ' ';
  }
}
