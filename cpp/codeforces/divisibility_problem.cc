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

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  vector<int> res(n);

  for (int i = 0; i < n; ++i) {
    int a, b;
    std::cin >> a >> b;
    if (a % b) {
      res[i] = b - (a % b);
    } else {
      res[i] = 0;
    }
  }

  for (int a : res) std::cout << a << "\n";

  return 0;
}
