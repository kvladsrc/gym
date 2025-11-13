#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <functional>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, f, k;
    std::cin >> n >> f >> k;
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    if (k == n) {
      std::cout << "YES"
                << "\n";
      continue;
    }

    int favorite = a[f - 1];
    // Sort a in reverse order.
    sort(a.begin(), a.end(), std::greater<int>());
    if (a[k - 1] > favorite) {
      std::cout << "NO"
                << "\n";
      continue;
    } else if (a[k - 1] < favorite) {
      std::cout << "YES"
                << "\n";
      continue;
    } else {  // a[k - 1] == favorite
      if (a[k] < favorite) {
        std::cout << "YES"
                  << "\n";
        continue;
      } else {  // a[k] == favorite also
        std::cout << "MAYBE"
                  << "\n";
        continue;
      }
    }
  }

  return 0;
}
