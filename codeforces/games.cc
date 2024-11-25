#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> host(n);
  unordered_map<int, int> guest;
  for (int i = 0; i < n; ++i) {
    std::cin >> host[i];
    int g_color;
    std::cin >> g_color;
    guest[g_color]++;
  }

  int res = 0;
  for (int h : host) {
    res += guest[h];
  }

  std::cout << res << "\n";

  return 0;
}
