#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int gcd(int a, int b) {
  if (a % b == 0) {
    return abs(b);
  }
  return gcd(b, a % b);
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    int cur_gcd = 0;
    for (auto &i : a) {
      std::cin >> i;
      cur_gcd = max(cur_gcd, i);
    }

    for (int i = 0; i < n; ++i) {
      for (int j = 1; j < n - i; ++j) {
        if (gcd(a[j], cur_gcd) < gcd(a[j - 1], cur_gcd)) {
          std::swap(a[j], a[j - 1]);
        }
      }

      cur_gcd = gcd(cur_gcd, a[(n - 1) - i]);
    }

    std::reverse(a.begin(), a.end());

    for (auto i : a) {
      std::cout << i << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
