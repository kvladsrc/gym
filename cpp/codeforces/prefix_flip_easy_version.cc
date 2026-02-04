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
using std::gcd;
using std::map;
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

void operation(string& a, int pref) {
  for (int i = 0; i <= pref; ++i) {
    a[i] = (a[i] == '1' ? '0' : '1');
  }

  std::reverse(a.begin(), a.begin() + pref + 1);
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    string a, b;
    std::cin >> a >> b;

    // Idea is to compare last bits until they dismatch b_i != a_1,
    // then use the operation on first prefix until a_0 != b_i, then
    // use operation on i prefix.
    vector<int> operaions;
    for (int i = n - 1; i >= 0; --i) {
      if (a[i] != b[i]) {
        if (a[0] == b[i]) {
          operaions.push_back(0);
          a[0] = (a[0] == '1' ? '0' : '1');
        }
        operation(a, i);
        operaions.push_back(i);
      }
    }

    std::cout << operaions.size() << " ";
    for (auto i : operaions) {
      std::cout << i + 1 << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
