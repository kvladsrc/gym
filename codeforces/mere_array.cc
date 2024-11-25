#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <numeric>
#include <queue>
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
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int gcd(int a, int b) {
  if (a % b == 0) return b;
  return gcd(b, a % b);
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    auto copy = a;
    sort(copy.begin(), copy.end());
    auto minimal = copy[0];
    bool res = true;
    for (int i = 0; i < n; ++i) {
      if (gcd(minimal, a[i]) != minimal) {
        if (a[i] != copy[i]) {
          res = false;
          break;
        }
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
