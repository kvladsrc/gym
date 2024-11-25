#include <algorithm>
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
    int num_even = 0, num_odd = 0;
    for (int i = 0; i < n; ++i) {
      std::cin >> a[i];
      if (a[i] % 2) {
        num_odd++;
      } else {
        num_even++;
      }
    }

    sort(a.begin(), a.end());
    int num_near = 0;
    for (int i = 1; i < n; ++i) {
      if (abs(a[i - 1] - a[i]) == 1) {
        num_near++;
        i++;
      }
    }

    if (num_even % 2 == 0 && num_odd % 2 == 0) {
      std::cout << "YES"
                << "\n";
      continue;
    }

    if (num_even % 2 && num_odd % 2 && num_near > 0) {
      std::cout << "YES"
                << "\n";
      continue;
    }

    std::cout << "NO"
              << "\n";
  }

  return 0;
}
