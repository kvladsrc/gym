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

string signup(const string& n, unordered_map<string, int>& m) {
  int i = m[n]++;
  return (i == 0 ? "OK" : n + std::to_string(i));
}

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  vector<string> names(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> names[i];
  }

  unordered_map<string, int> m;
  for (const string& name : names) {
    std::cout << signup(name, m) << "\n";
  }

  return 0;
}
