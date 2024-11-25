#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
    int i = 0, j = n - 1;
    while (i <= j) {
      int buf;
      std::cin >> buf;

      if (buf % 2) {
        a[j--] = buf;
      } else {
        a[i++] = buf;
      }
    }

    int max_pairs = 0;
    for (int i = 0; i < n; ++i) {
      if (a[i] % 2 == 0) {
        max_pairs += n - i - 1;
        continue;
      }

      for (int j = i + 1; j < n; ++j) {
        if (gcd(a[i], 2 * a[j]) > 1) max_pairs++;
      }
    }

    std::cout << max_pairs << "\n";
  }

  return 0;
}
