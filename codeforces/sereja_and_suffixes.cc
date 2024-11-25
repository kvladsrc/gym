#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
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
  int n, m;
  std::cin >> n >> m;

  vector<int> in(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> in[i];
  }

  vector<int> unique(n);
  unordered_map<int, bool> map;
  unique[n - 1] = 1;
  map[in[n - 1]] = true;
  for (int i = n - 2; i >= 0; --i) {
    if (map[in[i]]) {
      unique[i] = unique[i + 1];
    } else {
      unique[i] = unique[i + 1] + 1;
      map[in[i]] = true;
    }
  }

  while (m--) {
    int l;
    std::cin >> l;
    std::cout << unique[l - 1] << "\n";
  }

  return 0;
}
