#include <cstdlib>
#include <iostream>
#include <vector>

using std::vector;

namespace {

/*
  Sort array a, where a_i = {1, 2, 3}.
*/
void dutch_flag(vector<int> &a) {
  // EDGE_CASE: Empty or one element.
  if (a.size() <= 1) {
    return;
  }

  size_t l = 0;
  size_t m = 0;
  size_t r = a.size() - 1;

  while (m <= r) {
    switch (a[m]) {
      case 1:
        std::swap(a[m], a[l]);
        m++;
        l++;
        break;
      case 2:
        m++;
        break;
      default:
        std::swap(a[m], a[r]);
        if (r == 0) {
          return;
        }
        r--;
    }
  }
}

}  // namespace

int main(int /*argc*/, char * /*argv*/[]) {
  size_t n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  dutch_flag(a);
  for (auto i : a) {
    std::cout << i << " ";
  }
  std::cout << "\n";

  return 0;
}
