#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

static int upper_bound(vector<int> &a, int i) {
  int l = 0;
  int r = a.size() - 1;
  int res = -1;

  while (l <= r) {
    int const mid = (l + r) / 2;
    if (a[mid] >= i) {
      r = mid - 1;
      res = mid;
    } else {
      l = mid + 1;
    }
  }

  return res;
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  int el = 0;
  std::cin >> el;
  std::cout << upper_bound(a, el) << "\n";

  return 0;
}
