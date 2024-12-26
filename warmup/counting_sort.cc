#include <algorithm>
#include <boost/range/algorithm/sort.hpp>
#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

static void couting_sort(vector<int> &v) {
  int max_val = 0;

  for (auto i : v) {
    max_val = std::max(i, max_val);

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
    while (table[i] != 0) {
      v.push_back(i);
      table[i]--;
    }
  }
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  vector<int> b(n);
  for (int i = 0; i < n; ++i) {
    int buf = 0;
    std::cin >> buf;
    a[i] = b[i] = buf;
  }

  boost::range::sort(a);
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
