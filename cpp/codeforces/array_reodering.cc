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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;

  while (t--) {
    int n;
    std::cin >> n;

    vector<int> a(n);
    int left = 0, right = n - 1;
    while (left <= right) {
      int buf;
      std::cin >> buf;

      if (buf % 2) {
        a[right--] = buf;
      } else {
        a[left++] = buf;
      }
    }

    int max_pairs = 0;
    for (int idx = 0; idx < n; ++idx) {
      if (a[idx] % 2 == 0) {
        max_pairs += n - idx - 1;
        continue;
      }

      max_pairs += std::count_if(a.begin() + idx + 1, a.end(), [&](int val) {
        return gcd(a[idx], 2 * val) > 1;
      });
    }

    std::cout << max_pairs << "\n";
  }

  return 0;
}
