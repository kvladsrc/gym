#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

void couting_sort(vector<int> &v) {
  int max_val = 0;

  for (auto i : v) {
    if (i > max_val) {
      max_val = i;
    }

    if (i < 0) {
      return;
    }
  }

  vector<int> table(max_val + 1, 0);
  for (auto i : v) {
    table[i]++;
  }

  v.clear();
  for (size_t i = 1; i < table.size(); ++i) {
    while (table[i]) {
      v.push_back(i);
      table[i]--;
    }
  }
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> a(n);
  vector<int> b(n);
  for (int i = 0; i < n; ++i) {
    int buf;
    std::cin >> buf;
    a[i] = b[i] = buf;
  }

  sort(a.begin(), a.end());
  couting_sort(b);

  for (int i = 0; i < n; ++i) {
    if (a[i] != b[i]) {
      std::cout << "ERROR"
                << "\n";
    } else {
      std::cout << "OK"
                << "\n";
    }
  }

  return 0;
}
