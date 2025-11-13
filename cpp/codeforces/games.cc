#include <algorithm>
#include <iostream>
#include <numeric>
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

  int res =
      std::accumulate(host.begin(), host.end(), 0,
                      [&guest](int acc, int h) { return acc + guest[h]; });

  std::cout << res << "\n";

  return 0;
}
