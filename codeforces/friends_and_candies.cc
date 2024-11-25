#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    int64_t total = 0;
    for (int i = 0; i < n; ++i) {
      std::cin >> a[i];
      total += a[i];
    }

    if (total % n) {
      std::cout << -1 << "\n";
      continue;
    }

    int64_t avrg = total / n;
    int res = 0;
    for (int ai : a) {
      if (ai > avrg) res++;
    }
    std::cout << res << "\n";
  }

  return 0;
}
